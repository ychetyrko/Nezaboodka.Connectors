CREATE TABLE `${db_name}`.`secondary_index` (
	`index_id` INT PRIMARY KEY NOT NULL,
	`is_unique` BOOLEAN NOT NULL,

	FOREIGN KEY (`index_id`)
		REFERENCES `index_base`(`id`)
		ON DELETE CASCADE
) ENGINE=`INNODB`;
