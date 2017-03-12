DELIMITER //
DROP PROCEDURE IF EXISTS _prepare_new_database //
CREATE PROCEDURE _prepare_new_database(
	IN db_name VARCHAR(64)
)
BEGIN
/*---------------------------------------/
			Schema Info
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- type
		"CREATE TABLE `", db_name, "`.`type`(
			`id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
			`name` VARCHAR(128) NOT NULL UNIQUE
				CHECK(`name` != ''),
			`table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
				CHECK(`table_name` != ''),
			`base_type_name` VARCHAR(128) NOT NULL DEFAULT ''
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- type_closure
		"CREATE TABLE `", db_name, "`.`type_closure`(
			`ancestor` INT NOT NULL,
			`descendant` INT NOT NULL,
			
			FOREIGN KEY(`ancestor`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
			
			FOREIGN KEY(`descendant`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
			
			CONSTRAINT `uc_keys`
				UNIQUE (`ancestor`, `descendant`)
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- field
		"CREATE TABLE `", db_name, "`.`field` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_type_name` VARCHAR(128) NOT NULL
				CHECK(`owner_type_name` != ''),
			`owner_type_id` INT DEFAULT NULL,
			`name` VARCHAR(128) NOT NULL
				CHECK(`name` != ''),
			`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
				CHECK(`col_name` != ''),
			`type_name` VARCHAR(64) NOT NULL
				CHECK(`type_name` != ''),
			`is_nullable` BOOLEAN NOT NULL DEFAULT FALSE,
			`ref_type_id` INT DEFAULT NULL,
			`is_list` BOOLEAN NOT NULL DEFAULT FALSE,
			`compare_options` ENUM (
				'None',
				'IgnoreCase',
				'IgnoreNonSpace',
				'IgnoreSymbols',
				'IgnoreKanaType',
				'IgnoreWidth',
				'OrdinalIgnoreCase',
				'StringSort',
				'Ordinal'
			) NOT NULL DEFAULT 'None',
			`back_ref_name` VARCHAR(128) DEFAULT NULL
				CHECK(`back_ref_name` != ''),
			`back_ref_id` INT DEFAULT NULL,
			
			FOREIGN KEY(`owner_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,
			
			FOREIGN KEY(`ref_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE RESTRICT,	-- to prevent deletion of referenced type
			
			FOREIGN KEY(`back_ref_id`)
				REFERENCES `field`(`id`)
				ON DELETE SET NULL,

			CONSTRAINT `uc_type_fields`
				UNIQUE (`owner_type_name`, `name`),

			CONSTRAINT `uc_table_columns`
				UNIQUE (`owner_type_name`, `col_name`)
		) ENGINE=`INNODB`;"
	));

/*---------------------------------------/
			Default Structure
--------------------------------------*/
	CALL QEXEC(CONCAT(	-- db_key
		"CREATE TABLE `", db_name, "`.`db_key` (
			`sys_id` BIGINT(0) PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`rev_flags` BIGINT(0) NOT NULL DEFAULT 1,
			`real_type_id` INT NOT NULL,
			
			FOREIGN KEY (`real_type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE	-- delete key when it's type info is removed
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- list
		"CREATE TABLE `", db_name, "`.`list` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`owner_id` BIGINT(0) NOT NULL,
			`type_id` INT NOT NULL,
			`field_id` INT NOT NULL,
			
			FOREIGN KEY (`owner_id`)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE,	-- cleanup list of deleted object
			
			FOREIGN KEY (`type_id`)
				REFERENCES `type`(`id`)
				ON DELETE CASCADE,	-- delete list when it's type info is removed
			
			FOREIGN KEY (`field_id`)
				REFERENCES `field`(`id`)
				ON DELETE CASCADE	-- delete list when it's field info is removed
		) ENGINE=`INNODB`;"
	));
	CALL QEXEC(CONCAT(	-- list_item
		"CREATE TABLE `", db_name, "`.`list_item` (
			`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
			`list_id` INT NOT NULL,
			`ref` BIGINT(0) NOT NULL,
			
			FOREIGN KEY (`list_id`)
				REFERENCES `list`(`id`)
				ON DELETE CASCADE,	-- clear removed lists
			
			FOREIGN KEY (`ref`)
				REFERENCES `db_key`(`sys_id`)
				ON DELETE CASCADE,	-- remove deleted object from all lists

			CONSTRAINT `uc_list_ref`	-- no duplicates
				UNIQUE (`list_id`, `ref`)
		) ENGINE=`INNODB`;"
	));
END //
