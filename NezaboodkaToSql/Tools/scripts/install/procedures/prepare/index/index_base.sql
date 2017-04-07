CREATE TABLE `${db_name}`.`index_base` (
	`id` INT PRIMARY KEY AUTO_INCREMENT NOT NULL,
	`type_name` VARCHAR(255) NOT NULL,
	`type_id` INT NOT NULL,
	`index_type` ENUM (
		'secondary_index',
		'referencial_index'
	) NOT NULL,

	FOREIGN KEY (`type_id`)
		REFERENCES `type`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;
