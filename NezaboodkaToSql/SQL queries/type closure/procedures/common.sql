/*======================================

		Nezaboodka Admin database
			common procedures

======================================*/

USE `nz_test_closure`;


DELIMITER //
DROP PROCEDURE IF EXISTS QEXEC //
CREATE PROCEDURE QEXEC(
	IN query_text TEXT
)
BEGIN
	DECLARE is_prepared BOOLEAN DEFAULT FALSE;
	DECLARE EXIT HANDLER FOR SQLEXCEPTION BEGIN
		SET @prep_str = NULL;
		IF is_prepared THEN
			DEALLOCATE PREPARE p_prep_proc;
		END IF;
		RESIGNAL;
	END;
	SET @prep_str = query_text;

	PREPARE p_prep_proc FROM @prep_str;
	SET is_prepared = TRUE;

	EXECUTE p_prep_proc;

	DEALLOCATE PREPARE p_prep_proc;
	SET @prep_str = NULL;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_before_common //
CREATE PROCEDURE _temp_before_common()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`temp_type_fields`;
	CREATE TEMPORARY TABLE `nz_test_closure`.`temp_type_fields`(
		`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL UNIQUE,
		`col_name` VARCHAR(64) NOT NULL COLLATE `UTF8_GENERAL_CI`
			CHECK(`col_name` != ''),
		`type_name` VARCHAR(64) NOT NULL
			CHECK(`type_name` != ''),
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
		) NOT NULL DEFAULT 'None'
	) ENGINE=`MEMORY`;
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _temp_after_common //
CREATE PROCEDURE _temp_after_common()
BEGIN
	DROP TEMPORARY TABLE IF EXISTS `nz_test_closure`.`temp_type_fields`;   
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _get_type_new_fields_and_constraints //
CREATE PROCEDURE _get_type_new_fields_and_constraints(
	IN c_type_id INT,
	IN inheriting BOOLEAN,
	OUT fields_defs TEXT,
	OUT fields_constraints TEXT
)
BEGIN
	DECLARE cf_id INT DEFAULT NULL;	-- for constraints names
	DECLARE cf_col_name VARCHAR(64) DEFAULT NULL;
	DECLARE cf_type_name VARCHAR(128) DEFAULT NULL;
	DECLARE cf_ref_type_id INT DEFAULT NULL;
	DECLARE cf_is_list BOOLEAN DEFAULT FALSE;
	DECLARE cf_compare_options VARCHAR(64);

	SET fields_defs = '';
	SET fields_constraints = '';
/*
-- Debug
	SELECT concat('Start ', c_type_name, '(', c_type_id,')', ' altering.') AS debug;
*/
	DELETE FROM `nz_test_closure`.`temp_type_fields`;
	IF inheriting THEN	-- get all parents' fields
		INSERT INTO `nz_test_closure`.`temp_type_fields`
		(`id`, `col_name`, `type_name`, `ref_type_id`,
			`is_list`, `compare_options`)
		SELECT f.`id`, f.`col_name`, f.`type_name`, f.`ref_type_id`,
			f.`is_list`, f.`compare_options`
		FROM `nz_test_closure`.`field` AS f
		WHERE f.`owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `nz_test_closure`.`type_closure` AS clos
			WHERE clos.`descendant` = c_type_id
		);
	ELSE	-- get only NEW fields
		INSERT INTO `nz_test_closure`.`temp_type_fields`
		(`id`, `col_name`, `type_name`, `ref_type_id`,
			`is_list`, `compare_options`)
		SELECT f.`id`, f.`col_name`, f.`type_name`, f.`ref_type_id`,
			f.`is_list`, f.`compare_options`
		FROM `nz_test_closure`.`new_field` AS newf	-- only new fields
		LEFT JOIN `nz_test_closure`.`field` AS f
		ON f.`id` = newf.`id`
		WHERE f.`owner_type_id` IN (
			SELECT clos.`ancestor`	-- get all super classes
			FROM `nz_test_closure`.`type_closure` AS clos
			WHERE clos.`descendant` = c_type_id
		);
	END IF;

	BEGIN	
		DECLARE fields_done BOOLEAN DEFAULT FALSE;
		DECLARE fields_cur CURSOR FOR
			SELECT *
			FROM `nz_test_closure`.`temp_type_fields`;
		DECLARE CONTINUE HANDLER FOR NOT FOUND
			SET fields_done = TRUE;

		OPEN fields_cur;

		FETCH fields_cur
		INTO cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
			cf_is_list, cf_compare_options;
		WHILE NOT fields_done DO

			CALL _update_type_fields_def_constr(
				fields_defs, fields_constraints, inheriting,
				c_type_id, cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
				cf_is_list, cf_compare_options
			);
			
			FETCH fields_cur
			INTO cf_id, cf_col_name, cf_type_name, cf_ref_type_id,
				cf_is_list, cf_compare_options;
		END WHILE;
	END;

	IF (LEFT(fields_defs, 1) = ',') THEN
		SET fields_defs = SUBSTRING(fields_defs, 2);
	END IF;

	IF (LEFT(fields_constraints, 1) = ',') THEN
		SET fields_constraints = SUBSTRING(fields_constraints, 2);
	END IF;
/*
-- Debug
	SELECT fields_defs;
	SELECT fields_constraints;
	SELECT concat('END ', c_type_id, '(', c_type_id,')', ' altering.') AS debug;
*/
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _update_type_fields_def_constr //
CREATE PROCEDURE _update_type_fields_def_constr(
	INOUT f_defs TEXT,
	INOUT f_constrs TEXT,
	IN inheriting BOOLEAN,
	IN c_type_id INT,
	IN cf_id INT,
	IN cf_col_name VARCHAR(64),
	IN cf_type_name VARCHAR(128),
	IN cf_ref_type_id INT,
	IN cf_is_list BOOLEAN,
	IN cf_compare_options VARCHAR(128)
)
BEGIN
	DECLARE constr_add_prefix TEXT DEFAULT 'CONSTRAINT FK_';
	DECLARE constr_add_prefix_full TEXT DEFAULT '';
	DECLARE field_type VARCHAR(128);
	DECLARE nullable_sign_pos INT DEFAULT 0;

	IF NOT inheriting THEN
		SET constr_add_prefix = CONCAT('ADD ', constr_add_prefix);
	END IF;

	IF cf_ref_type_id IS NULL THEN
		IF NOT cf_is_list THEN
			SET field_type = cf_type_name;
			IF field_type LIKE 'VARCHAR(%' OR field_type = 'TEXT' THEN
				IF cf_compare_options = 'IgnoreCase' THEN
					SET field_type = CONCAT(field_type, ' COLLATE `utf8_general_ci`');
				END IF;
			ELSE	-- not string
				IF (RIGHT(field_type, 1) = '?') THEN
					SET field_type =
						SUBSTRING(field_type FROM 1 FOR CHAR_LENGTH(field_type)-1);
				ELSE
					SET field_type = CONCAT(field_type, ' NOT NULL');
				END IF;
			END IF;
			
		ELSE	-- list
			SET field_type = 'BLOB';
		END IF;
	ELSE	-- reference
		-- FK Constraint name = FK_<type_id>_<field_id>
		SET constr_add_prefix_full = CONCAT('
			', constr_add_prefix, c_type_id, '_', cf_id);
		
		IF NOT cf_is_list THEN
			SET field_type = 'BIGINT(0)';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `db_key`(`sys_id`)
					ON DELETE SET NULL
					ON UPDATE SET NULL');
		ELSE	-- list
			SET field_type = 'INT';
			SET f_constrs = CONCAT(f_constrs, ',
				', constr_add_prefix_full,'
				FOREIGN KEY (`', cf_col_name,'`)
					REFERENCES `list`(`id`)
					ON DELETE SET NULL');
		END IF;
	END IF;

	SET f_defs = CONCAT(f_defs, ', `', cf_col_name, '` ', field_type);
END //


DELIMITER //
DROP PROCEDURE IF EXISTS _remove_deleted_fields_from_table //
CREATE PROCEDURE _remove_deleted_fields_from_table()
BEGIN
	UPDATE `nz_test_closure`.`field`
	SET `back_ref_name` = NULL
	WHERE `back_ref_id` IN (
		SELECT `id`
		FROM `nz_test_closure`.`removing_fields_list`
	);

	DELETE FROM `nz_test_closure`.`field`
	WHERE `id` IN (
		SELECT `id`
		FROM `nz_test_closure`.`removing_fields_list`
	);
END //
